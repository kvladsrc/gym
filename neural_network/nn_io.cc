#include "nn_io.h"  // NOLINT(build/include_subdir)

#include <cstddef>
#include <fstream>
#include <iomanip>
#include <limits>
#include <stdexcept>
#include <string>

void NNIO::save(const NN& net, const std::string& path) {
  std::ofstream output(path);
  if (!output) {
    throw std::runtime_error("cannot open model for writing: " + path);
  }

  output << "NNv1\n"
         << net.input_size << ' '
         << std::setprecision(std::numeric_limits<double>::max_digits10)
         << net.step_size << ' ' << net.network.size() << '\n';

  for (std::size_t layer = 0; layer < net.network.size(); ++layer) {
    const std::size_t width =
        net.network[layer].empty() ? 0 : net.network[layer].front().size();
    output << net.network[layer].size() << ' ' << width << '\n';

    for (std::size_t neuron = 0; neuron < net.network[layer].size(); ++neuron) {
      output << net.biases[layer][neuron];
      for (const double weight : net.network[layer][neuron]) {
        output << ' ' << weight;
      }
      output << '\n';
    }
  }

  if (!output) {
    throw std::runtime_error("cannot write model: " + path);
  }
}

NN NNIO::load(const std::string& path) {
  std::ifstream input(path);
  if (!input) {
    throw std::runtime_error("cannot open model: " + path);
  }

  std::string format;
  std::size_t saved_input_size = 0;
  double saved_step_size = 0.0;
  std::size_t layer_count = 0;
  input >> format >> saved_input_size >> saved_step_size >> layer_count;

  if (!input || format != "NNv1" || saved_input_size == 0 || layer_count == 0) {
    throw std::runtime_error("invalid model header: " + path);
  }

  NN result(saved_input_size, saved_step_size, 0.0);
  for (std::size_t layer = 0; layer < layer_count; ++layer) {
    std::size_t neuron_count = 0;
    std::size_t width = 0;
    input >> neuron_count >> width;

    const std::size_t expected_width =
        layer == 0 ? saved_input_size : result.network.back().size();
    if (!input || neuron_count == 0 || width != expected_width) {
      throw std::runtime_error("invalid layer in model: " + path);
    }

    result.add_layer(neuron_count);
    for (std::size_t neuron = 0; neuron < neuron_count; ++neuron) {
      input >> result.biases[layer][neuron];
      for (double& weight : result.network[layer][neuron]) {
        input >> weight;
      }
    }

    if (!input) {
      throw std::runtime_error("incomplete model: " + path);
    }
  }

  return result;
}
