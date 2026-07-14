#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <random>
#include <string>
#include <vector>

#include "cifar10.h"  // NOLINT(build/include_subdir)
#include "nn_io.h"    // NOLINT(build/include_subdir)
#include "nn_lib.h"   // NOLINT(build/include_subdir)

namespace {

constexpr std::size_t kClassCount = 10;
constexpr std::size_t kEpochCount = 3;
constexpr std::size_t kBatchSize = 64;
constexpr double kL2Regularization = 1e-3;
constexpr double kStepSize = 0.05;

constexpr std::array<const char*, kClassCount> kClassNames = {
    "airplane", "automobile", "bird",  "cat",  "deer",
    "dog",      "frog",       "horse", "ship", "truck",
};

std::vector<V> make_examples() {
  std::vector<V> examples(kClassCount, V(kClassCount, 0.0));
  for (std::size_t label = 0; label < kClassCount; ++label) {
    examples[label][label] = 1.0;
  }
  return examples;
}

V normalize(const Cifar10Image& image) {
  V result(image.size());
  std::transform(image.begin(), image.end(), result.begin(),
                 [](const std::uint8_t pixel) {
                   return static_cast<double>(pixel) / 255.0;
                 });
  return result;
}

std::size_t predicted_label(const V& probabilities) {
  return static_cast<std::size_t>(std::distance(
      probabilities.begin(),
      std::max_element(probabilities.begin(), probabilities.end())));
}

void evaluate(NN& net, const Cifar10Dataset& dataset) {
  constexpr double epsilon = 1e-15;
  std::array<std::size_t, kClassCount> correct_by_class{};
  std::array<std::size_t, kClassCount> total_by_class{};
  std::size_t correct = 0;
  double loss = 0.0;

  for (std::size_t index = 0; index < dataset.images.size(); ++index) {
    const V probabilities = net.predict(normalize(dataset.images[index]));
    const std::size_t label = dataset.labels[index];
    const std::size_t prediction = predicted_label(probabilities);
    ++total_by_class[label];

    if (prediction == label) {
      ++correct;
      ++correct_by_class[label];
    }
    loss -= std::log(std::max(probabilities[label], epsilon));
  }

  const double test_size = static_cast<double>(dataset.images.size());
  std::cout << std::fixed << std::setprecision(2)
            << "test accuracy: " << (100.0 * correct / test_size) << "% ("
            << correct << '/' << dataset.images.size() << ")\n"
            << std::setprecision(6)
            << "test cross-entropy: " << (loss / test_size) << '\n';

  for (std::size_t label = 0; label < kClassCount; ++label) {
    const double class_size = static_cast<double>(total_by_class[label]);
    std::cout << std::setw(10) << kClassNames[label] << ": "
              << std::setprecision(2)
              << (100.0 * correct_by_class[label] / class_size) << "%\n";
  }
}

std::string batch_path(const std::filesystem::path& directory,
                       const std::string& filename) {
  return (directory / filename).string();
}

}  // namespace

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  if (argc != 3) {
    std::cerr << "usage: " << argv[0] << " CIFAR_DIRECTORY MODEL\n";
    return 1;
  }

  const std::filesystem::path directory = argv[1];
  std::vector<std::string> training_paths;
  for (std::size_t batch = 1; batch <= 5; ++batch) {
    training_paths.push_back(
        batch_path(directory, "data_batch_" + std::to_string(batch) + ".bin"));
  }

  const Cifar10Dataset training = load_cifar10(training_paths);
  const Cifar10Dataset test =
      load_cifar10({batch_path(directory, "test_batch.bin")});
  std::cout << "loaded " << training.images.size() << " training and "
            << test.images.size() << " test images, 32x32 RGB\n";

  std::vector<std::size_t> training_indices(training.images.size());
  std::iota(training_indices.begin(), training_indices.end(), 0);
  std::random_device random_device;
  std::mt19937 rng(random_device());
  const std::vector<V> examples = make_examples();

  NN net(kCifar10ImageSize, kStepSize, kL2Regularization);
  net.add_layer(512);
  net.add_layer(256);
  net.add_layer(10);

  for (std::size_t epoch = 0; epoch < kEpochCount; ++epoch) {
    std::shuffle(training_indices.begin(), training_indices.end(), rng);
    std::cout << "epoch " << (epoch + 1) << '/' << kEpochCount << '\n';

    for (std::size_t begin = 0; begin < training_indices.size();
         begin += kBatchSize) {
      const std::size_t end =
          std::min(begin + kBatchSize, training_indices.size());
      M input_batch;
      M example_batch;
      input_batch.reserve(end - begin);
      example_batch.reserve(end - begin);

      for (std::size_t position = begin; position < end; ++position) {
        const std::size_t index = training_indices[position];
        input_batch.push_back(normalize(training.images[index]));
        example_batch.push_back(examples[training.labels[index]]);
      }

      net.train(input_batch, example_batch);
    }
  }

  evaluate(net, test);
  NNIO::save(net, argv[2]);
  std::cout << "saved model to " << argv[2] << '\n';
  return 0;
}
