#include "nn_lib.h"  // NOLINT(build/include_subdir)

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <random>
#include <utility>

namespace {

double relu(double a) { return std::max(a, static_cast<double>(0)); }

// ReLU activation.
void activate(V& v) { std::transform(v.begin(), v.end(), v.begin(), relu); }

// He initialization for ReLU layers.
void initialize_he(M& layer) {
  if (layer.empty() || layer.front().empty()) {
    return;
  }

  static std::mt19937 rng([] {
    std::random_device random_device;
    return random_device();
  }());
  const double standard_deviation =
      std::sqrt(2.0 / static_cast<double>(layer.front().size()));
  std::normal_distribution<double> dist(0.0, standard_deviation);

  for (auto& neuron_weights : layer) {
    std::generate(neuron_weights.begin(), neuron_weights.end(),
                  [&dist] { return dist(rng); });
  }
}

// V * M.
V operator*(const V& v, const M& m) {
  V res;
  for (const auto& n : m) {
    double out = 0;
    for (std::size_t i = 0; i < n.size(); ++i) {
      out += n[i] * v[i];
    }
    res.push_back(out);
  }
  return res;
}

// V + V.
V operator+(const V& lhs, const V& rhs) {
  V res = lhs;
  for (std::size_t i = 0; i < res.size(); ++i) {
    res[i] += rhs[i];
  }
  return res;
}

// Transpose matrix for back propagation.
M transpose(const M& matrix) {
  if (matrix.empty()) {
    return {};
  }

  const size_t rows = matrix.size();
  const size_t cols = matrix.front().size();

  M result(cols, V(rows));

  for (size_t row = 0; row < rows; ++row) {
    for (size_t col = 0; col < cols; ++col) {
      result[col][row] = matrix[row][col];
    }
  }

  return result;
}

// Output values to probabilities.
void softmax(V& v) {
  if (v.empty()) {
    return;
  }

  const double maximum = *std::max_element(v.begin(), v.end());
  double sum = 0;

  for (auto& i : v) {
    i = std::exp(i - maximum);
    sum += i;
  }

  std::transform(v.begin(), v.end(), v.begin(),
                 [sum](const double value) { return value / sum; });
}

}  // namespace

NN::NN(std::size_t in, double astep_size, double aregularization)
    : input_size(in), step_size(astep_size), regularization(aregularization) {}

void NN::add_layer(std::size_t out) {
  auto width = input_size;

  if (!network.empty()) {
    width = network.back().size();
  }

  M new_layer(out, V(width));
  initialize_he(new_layer);

  network.push_back(std::move(new_layer));
  biases.emplace_back(out, 0.0);
  outs.emplace_back(out, 0.0);
  grads.emplace_back(out, V(width, 0.0));
  biases_grads.emplace_back(out, 0.0);
}

void NN::back_propagation(const V& example, double normalizer) {
  // P_j - Y_j is derivative for loss function Softmax +
  // Cross-entropy, no ReLU. Divide by the number of examples to get
  // the average batch gradient.
  V d = ans;
  for (size_t i = 0; i < d.size(); ++i) {
    d[i] = (d[i] - example[i]) * normalizer;
  }

  for (size_t l = 0; l < network.size(); ++l) {
    auto back_l = network.size() - 1 - l;

    for (size_t n = 0; n < d.size(); ++n) {
      biases_grads[back_l][n] += d[n];

      for (size_t w = 0; w < grads[back_l][n].size(); ++w) {
        // Accumulate gradient for batch to calculate the average.
        if (back_l == 0) {
          // Input vector.
          grads[back_l][n][w] += input[w] * d[n];
        } else {
          // Previous layer output.
          grads[back_l][n][w] += outs[back_l - 1][w] * d[n];
        }
      }
    }

    if (back_l == 0) {
      break;
    }

    auto tm = transpose(network[back_l]);
    d = d * tm;

    // ReLU derivation.
    for (size_t i = 0; i < d.size(); ++i) {
      if (outs[back_l - 1][i] == 0.0) {
        d[i] = 0.0;
      }
    }
  }
}

// Change weights and wipe out grads buffers.
void NN::apply_grads() {
  for (size_t l = 0; l < network.size(); ++l) {
    for (size_t row = 0; row < network[l].size(); ++row) {
      biases[l][row] -= step_size * biases_grads[l][row];
      biases_grads[l][row] = 0;

      for (size_t col = 0; col < network[l][row].size(); ++col) {
        network[l][row][col] -=
            step_size *
            (grads[l][row][col] + regularization * network[l][row][col]);
        grads[l][row][col] = 0.0;
      }
    }
  }
}

void NN::forward() {
  if (network.size() == 1) {
    outs[0] = (input * network[0]) + biases[0];
    ans = outs[0];
    softmax(ans);
    return;
  }

  // Input layer + ReLU.
  outs[0] = (input * network[0]) + biases[0];
  activate(outs[0]);

  // Hidden layers + ReLU.
  for (std::size_t i = 1; i < network.size(); ++i) {
    outs[i] = outs[i - 1] * network[i] + biases[i];

    // Do not apply ReLU on output layer (it erase order for negative
    // logits).
    if (i != network.size() - 1) {
      activate(outs[i]);
    }
  }

  ans = outs.back();
  softmax(ans);
}

void NN::train(const M& inputs, const M& examples) {
  // Needed to calculate average loss.
  const double normalizer = 1.0 / static_cast<double>(inputs.size());

  for (std::size_t i = 0; i < inputs.size(); ++i) {
    input = inputs[i];
    forward();
    back_propagation(examples[i], normalizer);
  }

  apply_grads();
}

V NN::predict(const V& in) {
  input = in;
  forward();
  return ans;
}
