#include <algorithm>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <random>
#include <vector>

#include "mnist.h"   // NOLINT(build/include_subdir)
#include "nn_io.h"   // NOLINT(build/include_subdir)
#include "nn_lib.h"  // NOLINT(build/include_subdir)

namespace {

constexpr std::size_t kClassCount = 10;
constexpr std::size_t kEpochCount = 3;
constexpr std::size_t kBatchSize = 64;
constexpr double kL2Regularization = 1e-3;
constexpr double kStepSize = 0.05;

std::vector<V> make_examples() {
  std::vector<V> examples(kClassCount, V(kClassCount, 0.0));
  for (std::size_t label = 0; label < kClassCount; ++label) {
    examples[label][label] = 1.0;
  }
  return examples;
}

std::size_t predicted_label(const V& probabilities) {
  return static_cast<std::size_t>(std::distance(
      probabilities.begin(),
      std::max_element(probabilities.begin(), probabilities.end())));
}

bool valid_dataset(const MnistDataset& dataset, const char* name) {
  if (dataset.images.empty() ||
      dataset.images.size() != dataset.labels.size()) {
    std::cerr << name << " dataset is empty or inconsistent\n";
    return false;
  }

  const bool has_invalid_label = std::any_of(
      dataset.labels.begin(), dataset.labels.end(),
      [](const std::uint8_t label) { return label >= kClassCount; });
  if (has_invalid_label) {
    std::cerr << name << " dataset contains an invalid label\n";
    return false;
  }

  return true;
}

void evaluate(NN& net, const MnistDataset& dataset) {
  constexpr double epsilon = 1e-15;
  std::size_t correct = 0;
  double loss = 0.0;

  for (std::size_t index = 0; index < dataset.images.size(); ++index) {
    const V probabilities = net.predict(dataset.images[index]);
    const std::size_t label = dataset.labels[index];

    if (predicted_label(probabilities) == label) {
      ++correct;
    }
    loss -= std::log(std::max(probabilities[label], epsilon));
  }

  const double test_size = static_cast<double>(dataset.images.size());
  std::cout << std::fixed << std::setprecision(2)
            << "test accuracy: " << (100.0 * correct / test_size) << "% ("
            << correct << '/' << dataset.images.size() << ")\n"
            << std::setprecision(6)
            << "test cross-entropy: " << (loss / test_size) << '\n';
}

}  // namespace

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  if (argc != 6) {
    std::cerr << "usage: " << argv[0]
              << " TRAIN_IMAGES TRAIN_LABELS TEST_IMAGES TEST_LABELS MODEL\n";
    return 1;
  }

  const MnistDataset training = load_mnist(argv[1], argv[2]);
  const MnistDataset test = load_mnist(argv[3], argv[4]);
  if (!valid_dataset(training, "training") || !valid_dataset(test, "test")) {
    return 1;
  }

  if (training.rows != test.rows || training.cols != test.cols) {
    std::cerr << "training and test image dimensions do not match\n";
    return 1;
  }

  std::cout << "loaded " << training.images.size() << " training and "
            << test.images.size() << " test images, " << training.rows << 'x'
            << training.cols << '\n';

  std::vector<std::size_t> training_indices(training.images.size());
  std::iota(training_indices.begin(), training_indices.end(), 0);

  std::random_device random_device;
  std::mt19937 rng(random_device());
  const std::vector<V> examples = make_examples();

  NN net(training.images.front().size(), kStepSize, kL2Regularization);
  net.add_layer(32);
  net.add_layer(32);
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
        input_batch.push_back(training.images[index]);
        example_batch.push_back(examples[training.labels[index]]);
      }

      net.train(input_batch, example_batch);
    }
  }

  evaluate(net, test);
  NNIO::save(net, argv[5]);
  std::cout << "saved model to " << argv[5] << '\n';
  return 0;
}
