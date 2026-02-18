#include "pet_project/fitness.hpp"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <iostream>
#include <limits>
#include <memory>
#include <numeric>
#include <string>
#include <vector>

#ifdef _OPENMP
#include <omp.h>
#endif

namespace evo {

namespace {

// ImageNet normalization constants
constexpr float kMean[] = {0.485f, 0.456f, 0.406f};
constexpr float kStd[] = {0.229f, 0.224f, 0.225f};
constexpr int kModelSize = 224;

}  // namespace

Fitness::Fitness(const Config& config)
    : config_(config), env_(ORT_LOGGING_LEVEL_WARNING, "evo") {
  Ort::SessionOptions opts;
  opts.SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_ENABLE_ALL);
  opts.SetIntraOpNumThreads(0);
  opts.SetInterOpNumThreads(0);

  // Enable CUDA provider if requested
  if (config.use_gpu) {
    try {
      OrtCUDAProviderOptions cuda_options;
      cuda_options.device_id = 0;
      cuda_options.arena_extend_strategy = 0;
      cuda_options.gpu_mem_limit = SIZE_MAX;
      cuda_options.cudnn_conv_algo_search = OrtCudnnConvAlgoSearchExhaustive;
      cuda_options.do_copy_in_default_stream = 1;

      opts.AppendExecutionProvider_CUDA(cuda_options);
      std::cout << "Using GPU (CUDA) for inference" << std::endl;
    } catch (const std::exception& e) {
      std::cerr << "Failed to enable CUDA provider: " << e.what() << std::endl;
      std::cerr << "Falling back to CPU" << std::endl;
    }
  } else {
    std::cout << "Using CPU for inference" << std::endl;
  }
  for (const auto& path : config.model_paths) {
    sessions_.push_back(
        std::make_unique<Ort::Session>(env_, path.c_str(), opts));
  }
}

std::vector<float> Fitness::preprocess(const std::vector<uint8_t>& rgba,
                                       int src_w, int src_h) {
  // Bilinear resize from src to 224x224, RGBA -> RGB normalized, NCHW layout
  std::vector<float> tensor(3 * kModelSize * kModelSize);

  float x_ratio = static_cast<float>(src_w) / kModelSize;
  float y_ratio = static_cast<float>(src_h) / kModelSize;

  for (int y = 0; y < kModelSize; y++) {
    float src_y = y * y_ratio;
    int y0 = static_cast<int>(src_y);
    int y1 = std::min(y0 + 1, src_h - 1);
    float fy = src_y - y0;

    for (int x = 0; x < kModelSize; x++) {
      float src_x = x * x_ratio;
      int x0 = static_cast<int>(src_x);
      int x1 = std::min(x0 + 1, src_w - 1);
      float fx = src_x - x0;

      for (int c = 0; c < 3; c++) {
        float v00 = rgba[(y0 * src_w + x0) * 4 + c];
        float v10 = rgba[(y0 * src_w + x1) * 4 + c];
        float v01 = rgba[(y1 * src_w + x0) * 4 + c];
        float v11 = rgba[(y1 * src_w + x1) * 4 + c];

        float val = v00 * (1 - fx) * (1 - fy) + v10 * fx * (1 - fy) +
                    v01 * (1 - fx) * fy + v11 * fx * fy;

        // Normalize to [0,1] then apply ImageNet normalization
        val = (val / 255.0f - kMean[c]) / kStd[c];
        tensor[c * kModelSize * kModelSize + y * kModelSize + x] = val;
      }
    }
  }

  return tensor;
}

float Fitness::run_model(Ort::Session& session,
                         const std::vector<float>& batch) {
  std::array<int64_t, 4> input_shape = {2, 3, kModelSize, kModelSize};
  auto memory_info =
      Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
  auto input_tensor = Ort::Value::CreateTensor<float>(
      memory_info, const_cast<float*>(batch.data()), batch.size(),
      input_shape.data(), input_shape.size());

  auto input_name = session.GetInputNameAllocated(0, allocator_);
  auto output_name = session.GetOutputNameAllocated(0, allocator_);
  const char* input_names[] = {input_name.get()};
  const char* output_names[] = {output_name.get()};

  auto output_tensors = session.Run(Ort::RunOptions{nullptr}, input_names,
                                    &input_tensor, 1, output_names, 1);

  float* logits = output_tensors[0].GetTensorMutableData<float>();
  auto shape = output_tensors[0].GetTensorTypeAndShapeInfo().GetShape();
  int num_classes = static_cast<int>(shape[1]);

  // Return raw logit value for target class (min across augmentations).
  // This allows continuous improvement beyond softmax saturation.
  float best = std::numeric_limits<float>::max();
  for (int b = 0; b < 2; b++) {
    const float* row = logits + b * num_classes;
    best = std::min(best, row[config_.imagenet_class]);
  }
  return best;
}

float Fitness::evaluate(const std::vector<uint8_t>& rgba, int width,
                        int height) {
  auto original = preprocess(rgba, width, height);

  // Create horizontally flipped version directly on the tensor (NCHW layout).
  auto flipped = original;
  for (int c = 0; c < 3; c++) {
    for (int y = 0; y < kModelSize; y++) {
      int row_offset = c * kModelSize * kModelSize + y * kModelSize;
      std::reverse(flipped.begin() + row_offset,
                   flipped.begin() + row_offset + kModelSize);
    }
  }

  // Pack both into a single batch=2 tensor.
  const int image_floats = 3 * kModelSize * kModelSize;
  std::vector<float> batch(2 * image_floats);
  std::copy(original.begin(), original.end(), batch.begin());
  std::copy(flipped.begin(), flipped.end(), batch.begin() + image_floats);

  // Run each model, take min across all models and augmentations.
  float best = std::numeric_limits<float>::max();
  for (auto& session : sessions_) {
    best = std::min(best, run_model(*session, batch));
  }
  return best;
}

void Fitness::evaluate_population(std::vector<Individual>& population,
                                  const Renderer& renderer) {
#pragma omp parallel for schedule(dynamic)
  for (size_t i = 0; i < population.size(); i++) {
    auto image = renderer.render(population[i].dna);
    population[i].fitness =
        evaluate(image, config_.image_width, config_.image_height);
  }
}

}  // namespace evo
