// Runs one Audio2Face-3D model over one track of speech, offline, through the
// SDK's blendshape-solve executor bundle:
//
//   a2f-runner MODEL_JSON AUDIO_F32 OUT_PREFIX [--identity N] [--fps N]
//              [--emotion v,v,...]
//
// MODEL_JSON  the model's model.json, its TensorRT engine (network.trt) built
// AUDIO_F32   mono float32 samples at 16 kHz, little endian, nothing else
// --identity  diffusion models: the actor, an index into network_info.json's
//             identities (default 0)
// --fps       regression models: frames per second (default 30); diffusion
//             models have their own rate
// --emotion   the model's explicit emotions, comma separated, held for the
//             whole track (default all 0)
//
// Writes OUT_PREFIX.json (pose names, frame rate, sizes, timestamps) and
// OUT_PREFIX.f32: per frame the skin then tongue blendshape weights, the jaw
// transform (4x4, column major, from the neutral jaw to the animated one) and
// the eye rotations (right x, y, z then left x, y, z), all float32.

#include <cuda_runtime_api.h>

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <map>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

#include "audio2face/audio2face.h"
#include "audio2x/cuda_utils.h"

namespace {

[[noreturn]] void Fail(const std::string& message) {
  std::fprintf(stderr, "a2f-runner: %s\n", message.c_str());
  std::exit(1);
}

void Check(std::error_code error, const char* what) {
  if (error) {
    Fail(std::string(what) + ": " + error.message());
  }
}

void CheckCuda(cudaError_t error, const char* what) {
  if (error != cudaSuccess) {
    Fail(std::string(what) + ": " + cudaGetErrorString(error));
  }
}

struct Destroyer {
  template <typename T>
  void operator()(T* object) const {
    object->Destroy();
  }
};
template <typename T>
using Owned = std::unique_ptr<T, Destroyer>;

std::vector<float> ReadSamples(const char* path) {
  std::ifstream file(path, std::ios::binary | std::ios::ate);
  if (!file) {
    Fail(std::string("cannot read ") + path);
  }
  const auto bytes = static_cast<std::size_t>(file.tellg());
  if (bytes == 0 || bytes % sizeof(float) != 0) {
    Fail(std::string(path) + " is not a list of float32 samples");
  }
  std::vector<float> samples(bytes / sizeof(float));
  file.seekg(0);
  file.read(reinterpret_cast<char*>(samples.data()),
            static_cast<std::streamsize>(bytes));
  return samples;
}

std::vector<float> ParseList(const std::string& text) {
  std::vector<float> values;
  std::stringstream stream(text);
  std::string item;
  while (std::getline(stream, item, ',')) {
    values.push_back(std::stof(item));
  }
  return values;
}

std::string Quoted(const char* text) {
  std::string out = "\"";
  for (const char* c = text; *c; ++c) {
    if (*c == '"' || *c == '\\') {
      out += '\\';
    }
    out += *c;
  }
  return out + "\"";
}

// Device results of one frame, copied to the host.
struct Frame {
  std::vector<float> weights;
  std::vector<float> jaw;
  std::vector<float> eyes;
};

struct Collected {
  std::mutex mutex;
  std::map<std::int64_t, Frame> frames;
  std::size_t weightCount = 0;
  std::error_code error;
};

std::vector<float> ToHost(nva2x::DeviceTensorFloatConstView view,
                          cudaStream_t stream) {
  std::vector<float> values(view.Size());
  if (!values.empty()) {
    CheckCuda(cudaStreamSynchronize(stream), "waiting for the results");
    Check(nva2x::CopyDeviceToHost(
              nva2x::HostTensorFloatView{values.data(), values.size()}, view),
          "copying the results");
  }
  return values;
}

}  // namespace

int main(int argc, char** argv) {
  if (argc < 4) {
    Fail(
        "usage: a2f-runner MODEL_JSON AUDIO_F32 OUT_PREFIX [--identity N] "
        "[--fps N] [--emotion v,...]");
  }
  const char* modelPath = argv[1];
  const char* audioPath = argv[2];
  const std::string outPrefix = argv[3];
  std::size_t identity = 0;
  std::size_t fps = 30;
  std::vector<float> emotion;
  for (int i = 4; i < argc; ++i) {
    const std::string option = argv[i];
    if (i + 1 >= argc) {
      Fail("missing value for " + option);
    }
    const std::string value = argv[++i];
    if (option == "--identity") {
      identity = std::stoul(value);
    } else if (option == "--fps") {
      fps = std::stoul(value);
    } else if (option == "--emotion") {
      emotion = ParseList(value);
    } else {
      Fail("unknown option " + option);
    }
  }

  Check(nva2x::SetCudaDeviceIfNeeded(0), "selecting the GPU");

  // The network type decides which bundle reads the model.
  Owned<nva2f::IBlendshapeExecutorBundle> bundle;
  {
    std::ifstream model(modelPath);
    if (!model) {
      Fail(std::string("cannot read ") + modelPath);
    }
    const std::string text((std::istreambuf_iterator<char>(model)),
                           std::istreambuf_iterator<char>());
    const bool diffusion =
        text.find("\"modelConfigPaths\"") != std::string::npos;
    const auto everything = nva2f::IGeometryExecutor::ExecutionOption::All;
    // The CPU solver (bounded least squares) for every frame, not the
    // GPU one: offline, exactness over speed.
    if (diffusion) {
      bundle.reset(nva2f::ReadDiffusionBlendshapeSolveExecutorBundle(
          1, modelPath, everything, false, identity, false, nullptr, nullptr));
    } else {
      bundle.reset(nva2f::ReadRegressionBlendshapeSolveExecutorBundle(
          1, modelPath, everything, false, fps, 1, nullptr, nullptr));
    }
    if (!bundle) {
      Fail(std::string("cannot load the model ") + modelPath);
    }
  }
  auto& executor = bundle->GetExecutor();
  const cudaStream_t stream = bundle->GetCudaStream().Data();

  // The blendshape executor narrows its geometry executor to what it solves,
  // the skin and the tongue; the jaw and the eyes come from the geometry, so
  // ask for them again (allowed: the bundle was created with all of them).
  nva2f::IGeometryExecutor* geometry = nullptr;
  Check(nva2f::GetExecutorGeometryExecutor(executor, &geometry),
        "getting the geometry executor");
  Check(geometry->SetExecutionOption(
            nva2f::IGeometryExecutor::ExecutionOption::All),
        "asking for the jaw and the eyes");

  auto& emotions = bundle->GetEmotionAccumulator(0);
  if (emotion.empty()) {
    emotion.assign(emotions.GetEmotionSize(), 0.0f);
  }
  if (emotion.size() != emotions.GetEmotionSize()) {
    Fail("the model takes " + std::to_string(emotions.GetEmotionSize()) +
         " emotions, got " + std::to_string(emotion.size()));
  }
  Check(emotions.Accumulate(
            0, nva2x::HostTensorFloatConstView{emotion.data(), emotion.size()},
            stream),
        "setting the emotions");
  Check(emotions.Close(), "setting the emotions");

  const auto samples = ReadSamples(audioPath);
  auto& audio = bundle->GetAudioAccumulator(0);
  Check(audio.Accumulate(
            nva2x::HostTensorFloatConstView{samples.data(), samples.size()},
            stream),
        "reading the audio");
  Check(audio.Close(), "reading the audio");

  Collected collected;
  collected.weightCount = executor.GetWeightCount();

  // Jaw and eyes come with the geometry, before the solve; the weights come
  // from the solver, possibly later and from another thread.
  auto geometryCallback = [](void* userdata,
                             const nva2f::IGeometryExecutor::Results& results) {
    auto& data = *static_cast<Collected*>(userdata);
    auto jaw = ToHost(results.jawTransform, results.jawCudaStream);
    auto eyes = ToHost(results.eyesRotation, results.eyesCudaStream);
    std::lock_guard<std::mutex> lock(data.mutex);
    auto& frame = data.frames[results.timeStampCurrentFrame];
    frame.jaw = std::move(jaw);
    frame.eyes = std::move(eyes);
    return true;
  };
  Check(nva2f::SetExecutorGeometryResultsCallback(executor, geometryCallback,
                                                  &collected),
        "setting the geometry callback");
  auto weightsCallback =
      [](void* userdata, const nva2f::IBlendshapeExecutor::HostResults& results,
         std::error_code error) {
        auto& data = *static_cast<Collected*>(userdata);
        std::lock_guard<std::mutex> lock(data.mutex);
        if (error) {
          data.error = error;
          return;
        }
        data.frames[results.timeStampCurrentFrame].weights.assign(
            results.weights.Data(),
            results.weights.Data() + results.weights.Size());
      };
  Check(executor.SetResultsCallback(weightsCallback, &collected),
        "setting the results callback");

  while (nva2x::GetNbReadyTracks(executor) > 0) {
    Check(executor.Execute(nullptr), "running the model");
  }
  Check(executor.Wait(0), "finishing the solve");
  if (collected.error) {
    Fail("blendshape solve: " + collected.error.message());
  }

  // Pose names, in the order of the weights.
  std::vector<std::string> skinNames, tongueNames;
  nva2f::IBlendshapeSolver* solver = nullptr;
  if (!nva2f::GetExecutorSkinSolver(executor, 0, &solver) && solver) {
    for (int i = 0; i < solver->NumBlendshapePoses(); ++i) {
      skinNames.emplace_back(solver->GetPoseName(static_cast<std::size_t>(i)));
    }
  }
  solver = nullptr;
  if (!nva2f::GetExecutorTongueSolver(executor, 0, &solver) && solver) {
    for (int i = 0; i < solver->NumBlendshapePoses(); ++i) {
      tongueNames.emplace_back(
          solver->GetPoseName(static_cast<std::size_t>(i)));
    }
  }

  std::size_t rateNumerator = 0, rateDenominator = 1;
  executor.GetFrameRate(rateNumerator, rateDenominator);
  const std::size_t expected = executor.GetTotalNbFrames(0);
  std::size_t jawSize = 0, eyesSize = 0;
  for (const auto& [timestamp, frame] : collected.frames) {
    if (frame.weights.size() != collected.weightCount) {
      Fail("a frame has no blendshape weights");
    }
    if (jawSize == 0) {
      jawSize = frame.jaw.size();
      eyesSize = frame.eyes.size();
    }
    if (frame.jaw.size() != jawSize || frame.eyes.size() != eyesSize) {
      Fail("a frame has no jaw or eye results");
    }
  }
  if (collected.frames.size() != expected) {
    Fail("expected " + std::to_string(expected) + " frames, got " +
         std::to_string(collected.frames.size()));
  }

  std::ofstream data(outPrefix + ".f32", std::ios::binary);
  for (const auto& [timestamp, frame] : collected.frames) {
    for (const auto* values : {&frame.weights, &frame.jaw, &frame.eyes}) {
      data.write(reinterpret_cast<const char*>(values->data()),
                 static_cast<std::streamsize>(values->size() * sizeof(float)));
    }
  }
  if (!data) {
    Fail("cannot write " + outPrefix + ".f32");
  }

  std::ofstream info(outPrefix + ".json");
  info << "{\n  \"frames\": " << collected.frames.size()
       << ",\n  \"frame_rate\": [" << rateNumerator << ", " << rateDenominator
       << "]"
       << ",\n  \"jaw_size\": " << jawSize << ",\n  \"eyes_size\": " << eyesSize
       << ",\n  \"skin_poses\": [";
  for (std::size_t i = 0; i < skinNames.size(); ++i) {
    info << (i ? ", " : "") << Quoted(skinNames[i].c_str());
  }
  info << "],\n  \"tongue_poses\": [";
  for (std::size_t i = 0; i < tongueNames.size(); ++i) {
    info << (i ? ", " : "") << Quoted(tongueNames[i].c_str());
  }
  info << "],\n  \"timestamps\": [";
  std::size_t index = 0;
  for (const auto& [timestamp, frame] : collected.frames) {
    info << (index++ ? ", " : "") << timestamp;
  }
  info << "]\n}\n";
  if (!info) {
    Fail("cannot write " + outPrefix + ".json");
  }
  return 0;
}
