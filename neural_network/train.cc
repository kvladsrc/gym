#include <algorithm>
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

#include "cifar10.h"        // NOLINT(build/include_subdir)
#include "dataset.h"        // NOLINT(build/include_subdir)
#include "mnist.h"          // NOLINT(build/include_subdir)
#include "nn_io.h"          // NOLINT(build/include_subdir)
#include "nn_lib/nn_lib.h"  // NOLINT(build/include_subdir)

namespace {

struct TrainingOptions {
  std::vector<std::size_t> hidden_layers;
  std::size_t epochs = 10;
  std::size_t batch_size = 64;
  double step_size = 0.05;
  double regularization = 1e-3;
  double dropout_keep = 0.8;
};

DatasetSplit load_mnist_data(const std::filesystem::path& directory) {
  return {
      load_mnist((directory / "train-images-idx3-ubyte").string(),
                 (directory / "train-labels-idx1-ubyte").string()),
      load_mnist((directory / "t10k-images-idx3-ubyte").string(),
                 (directory / "t10k-labels-idx1-ubyte").string()),
  };
}

DatasetSplit load_cifar10_data(const std::filesystem::path& directory) {
  const std::filesystem::path batches = directory / "cifar-10-batches-bin";
  std::vector<std::string> training_paths;
  for (std::size_t batch = 1; batch <= 5; ++batch) {
    training_paths.push_back(
        (batches / ("data_batch_" + std::to_string(batch) + ".bin")).string());
  }

  return {
      load_cifar10(training_paths),
      load_cifar10({(batches / "test_batch.bin").string()}),
  };
}

bool valid_dataset(const Dataset& dataset, const char* name) {
  const std::size_t image_size = dataset.rows * dataset.cols * dataset.channels;
  const bool invalid_image =
      image_size == 0 ||
      std::any_of(dataset.images.begin(), dataset.images.end(),
                  [image_size](const Image& image) {
                    return image.size() != image_size;
                  });
  const bool invalid_label =
      dataset.class_count == 0 ||
      std::any_of(dataset.labels.begin(), dataset.labels.end(),
                  [&dataset](const std::uint8_t label) {
                    return label >= dataset.class_count;
                  });

  if (dataset.images.empty() ||
      dataset.images.size() != dataset.labels.size() || invalid_image ||
      invalid_label) {
    std::cerr << name << " dataset is empty or inconsistent\n";
    return false;
  }
  return true;
}

V normalize(const Image& image) {
  V result(image.size());
  std::transform(image.begin(), image.end(), result.begin(),
                 [](const std::uint8_t pixel) {
                   return static_cast<double>(pixel) / 255.0;
                 });
  return result;
}

M make_examples(std::size_t class_count) {
  M examples(class_count, V(class_count, 0.0));
  for (std::size_t label = 0; label < class_count; ++label) {
    examples[label][label] = 1.0;
  }
  return examples;
}

std::size_t predicted_label(const V& probabilities) {
  return static_cast<std::size_t>(std::distance(
      probabilities.begin(),
      std::max_element(probabilities.begin(), probabilities.end())));
}

std::size_t parameter_count(std::size_t input_size,
                            const TrainingOptions& options,
                            std::size_t class_count) {
  std::size_t count = 0;
  std::size_t previous_size = input_size;
  for (const std::size_t layer_size : options.hidden_layers) {
    count += (previous_size + 1) * layer_size;
    previous_size = layer_size;
  }
  return count + (previous_size + 1) * class_count;
}

void print_training_options(const Dataset& dataset,
                            const TrainingOptions& options) {
  const std::size_t input_size = dataset.images.front().size();
  std::cout << "network: " << input_size;
  for (const std::size_t layer_size : options.hidden_layers) {
    std::cout << " -> " << layer_size;
  }
  std::cout << " -> " << dataset.class_count << " ("
            << parameter_count(input_size, options, dataset.class_count)
            << " parameters)\n"
            << "training: epochs=" << options.epochs
            << ", batch_size=" << options.batch_size
            << ", step_size=" << options.step_size << '\n'
            << "regularization: l2=" << options.regularization
            << ", dropout_keep=" << options.dropout_keep << '\n'
            << std::flush;
}

void train_model(NN& net, const Dataset& dataset,
                 const TrainingOptions& options) {
  std::vector<std::size_t> indices(dataset.images.size());
  std::iota(indices.begin(), indices.end(), 0);

  std::random_device random_device;
  std::mt19937 rng(random_device());
  const M examples = make_examples(dataset.class_count);

  for (std::size_t epoch = 0; epoch < options.epochs; ++epoch) {
    std::shuffle(indices.begin(), indices.end(), rng);
    std::cout << "epoch " << (epoch + 1) << '/' << options.epochs << '\n'
              << std::flush;

    for (std::size_t begin = 0; begin < indices.size();
         begin += options.batch_size) {
      const std::size_t end =
          std::min(begin + options.batch_size, indices.size());
      M input_batch;
      M example_batch;
      input_batch.reserve(end - begin);
      example_batch.reserve(end - begin);

      for (std::size_t position = begin; position < end; ++position) {
        const std::size_t index = indices[position];
        input_batch.push_back(normalize(dataset.images[index]));
        example_batch.push_back(examples[dataset.labels[index]]);
      }
      net.train(input_batch, example_batch);
    }
  }
}

void evaluate(NN& net, const Dataset& dataset, const char* split) {
  constexpr double epsilon = 1e-15;
  std::size_t correct = 0;
  double loss = 0.0;

  for (std::size_t index = 0; index < dataset.images.size(); ++index) {
    const V probabilities = net.predict(normalize(dataset.images[index]));
    const std::size_t label = dataset.labels[index];
    if (predicted_label(probabilities) == label) {
      ++correct;
    }
    loss -= std::log(std::max(probabilities[label], epsilon));
  }

  const double dataset_size = static_cast<double>(dataset.images.size());
  std::cout << std::fixed << std::setprecision(2) << split
            << " accuracy: " << (100.0 * correct / dataset_size) << "% ("
            << correct << '/' << dataset.images.size() << ")\n"
            << std::setprecision(6) << split
            << " loss (cross-entropy): " << (loss / dataset_size) << '\n';
}

}  // namespace

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  if (argc != 4) {
    std::cerr << "usage: " << argv[0]
              << " {mnist|cifar10} DATA_DIRECTORY MODEL\n";
    return 1;
  }

  const std::string dataset_name = argv[1];
  const std::filesystem::path data_directory = argv[2];
  DatasetSplit datasets;
  TrainingOptions options;

  if (dataset_name == "mnist") {
    datasets = load_mnist_data(data_directory);
    options.hidden_layers = {256, 128};
  } else if (dataset_name == "cifar10") {
    datasets = load_cifar10_data(data_directory);
    options.hidden_layers = {512, 256};
    options.regularization = 1e-3;
  } else {
    std::cerr << "unknown dataset: " << dataset_name << '\n';
    return 1;
  }

  if (!valid_dataset(datasets.training, "training") ||
      !valid_dataset(datasets.test, "test")) {
    return 1;
  }
  if (datasets.training.rows != datasets.test.rows ||
      datasets.training.cols != datasets.test.cols ||
      datasets.training.channels != datasets.test.channels ||
      datasets.training.class_count != datasets.test.class_count) {
    std::cerr << "training and test dataset shapes do not match\n";
    return 1;
  }

  const Dataset& training = datasets.training;
  std::cout << "loaded " << training.images.size() << " training and "
            << datasets.test.images.size() << " test images, " << training.rows
            << 'x' << training.cols << 'x' << training.channels << '\n'
            << std::flush;
  print_training_options(training, options);

  NN net(training.images.front().size(), options.step_size,
         options.regularization, options.dropout_keep);
  for (const std::size_t layer_size : options.hidden_layers) {
    net.add_layer(layer_size);
  }
  net.add_layer(training.class_count);

  train_model(net, training, options);
  evaluate(net, training, "train");
  evaluate(net, datasets.test, "test");
  NNIO::save(net, argv[3]);
  std::cout << "saved model to " << argv[3] << '\n';
  return 0;
}
