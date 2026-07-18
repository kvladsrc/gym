#include "nn_lib.h"  // NOLINT(build/include_subdir)

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <random>
#include <utility>

// Returns the ReLU activation of a scalar.
double relu(double a) { return std::max(a, static_cast<double>(0)); }

// Applies ReLU element-wise.
void activate(V& v) { std::transform(v.begin(), v.end(), v.begin(), relu); }

// Inverse dropout. Scales elements by 1/p with p chance or 0.0 in
// place.
void dropout(V& v, double p) {
  static thread_local std::mt19937 rng([] {
    std::random_device random_device;
    return random_device();
  }());
  std::bernoulli_distribution keep(p);

  std::transform(v.begin(), v.end(), v.begin(),
                 [&](double value) { return keep(rng) ? value / p : 0.0; });
}

// Converts logits to probabilities with softmax.
void softmax(V& v) {
  const double maximum = *std::max_element(v.begin(), v.end());
  double sum = 0;

  for (auto& i : v) {
    i = std::exp(i - maximum);
    sum += i;
  }

  std::transform(v.begin(), v.end(), v.begin(),
                 [sum](const double value) { return value / sum; });
}

namespace {

// ReLU friendly initialization. Initializes weights with the He
// normal distribution.
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

// Returns the dot product of the vector with each matrix row.
V operator*(const V& v, const M& m) {
  V res(m.size(), 0.0);

  for (std::size_t row = 0; row < m.size(); ++row) {
    for (std::size_t col = 0; col < m[row].size(); ++col) {
      res[row] += m[row][col] * v[col];
    }
  }

  return res;
}

// Multiplies every vector element by a scalar.
V operator*(const V& v, double scalar) {
  V res = v;
  std::transform(res.begin(), res.end(), res.begin(),
                 [scalar](double value) { return value * scalar; });
  return res;
}

// Subtracts vectors element-wise in place.
V& operator-=(V& lhs, const V& rhs) {
  for (std::size_t i = 0; i < lhs.size(); ++i) {
    lhs[i] -= rhs[i];
  }
  return lhs;
}

// Adds vectors element-wise in place.
V& operator+=(V& lhs, const V& rhs) {
  for (std::size_t i = 0; i < lhs.size(); ++i) {
    lhs[i] += rhs[i];
  }
  return lhs;
}

// Multiplies a vector by a scalar in place.
V& operator*=(V& lhs, double scalar) {
  std::transform(lhs.begin(), lhs.end(), lhs.begin(),
                 [scalar](double value) { return value * scalar; });
  return lhs;
}

// Returns the matrix transpose.
M transpose(const M& m) {
  const size_t rows = m.size();
  const size_t cols = m.front().size();

  M result(cols, V(rows));

  for (size_t row = 0; row < rows; ++row) {
    for (size_t col = 0; col < cols; ++col) {
      result[col][row] = m[row][col];
    }
  }

  return result;
}

}  // namespace

NN::NN(std::size_t in, double astep_size, double aregularization,
       double adropout_keep_probability)
    : input_size(in),
      step_size(astep_size),
      regularization(aregularization),
      dropout_keep_probability(adropout_keep_probability) {}

void NN::add_layer(std::size_t out) {
  if (out == 0) {
    return;
  }

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
  // P_j - Y_j is the derivative of softmax cross-entropy with respect
  // to output logit j. Divide by the batch size to obtain the mean
  // gradient.
  V d = ans;
  for (size_t i = 0; i < d.size(); ++i) {
    d[i] -= example[i];
    d[i] *= normalizer;
  }

  for (size_t l = 0; l < network.size(); ++l) {
    auto back_l = network.size() - 1 - l;

    biases_grads[back_l] += d;

    // Accumulate this example's contribution to the batch gradient.
    for (size_t n = 0; n < d.size(); ++n) {
      if (back_l == 0) {
        // Input vector.
        grads[back_l][n] += input * d[n];
      } else {
        // Previous layer output.
        grads[back_l][n] += outs[back_l - 1] * d[n];
      }
    }

    if (back_l == 0) {
      break;
    }

    d = d * transpose(network[back_l]);
    d *= 1.0 / dropout_keep_probability;

    // A zero stored activation means either ReLU rejected the neuron or
    // dropout removed it. Apply both derivatives with their shared mask.
    for (size_t i = 0; i < d.size(); ++i) {
      if (outs[back_l - 1][i] == 0.0) {
        d[i] = 0.0;
      }
    }
  }
}

// Update parameters and clear the gradient buffers.
void NN::apply_grads() {
  for (size_t l = 0; l < network.size(); ++l) {
    // Update biases.
    biases_grads[l] *= step_size;
    biases[l] -= biases_grads[l];

    // Wipe out bias gradients.
    std::fill(biases_grads[l].begin(), biases_grads[l].end(), 0.0);

    for (size_t row = 0; row < network[l].size(); ++row) {
      // L2 regularization.
      grads[l][row] += network[l][row] * regularization;
      grads[l][row] *= step_size;
      network[l][row] -= grads[l][row];

      // Wipe out gradients.
      std::fill(grads[l][row].begin(), grads[l][row].end(), 0.0);
    }
  }
}

void NN::forward_pass(bool inference) {
  // Compute the first layer and apply ReLU if it is hidden.
  outs[0] = input * network[0];
  outs[0] += biases[0];

  // A single-layer network has no hidden layers.
  if (network.size() > 1) {
    activate(outs[0]);

    if (!inference) {
      dropout(outs[0], dropout_keep_probability);
    }
  }

  // Compute the remaining layers and apply ReLU to hidden layers.
  for (std::size_t i = 1; i < network.size(); ++i) {
    outs[i] = outs[i - 1] * network[i];
    outs[i] += biases[i];

    // Do not apply ReLU and Dropout to the output layer: it would
    // collapse all negative logits to zero.
    if (i != network.size() - 1) {
      activate(outs[i]);

      if (!inference) {
        dropout(outs[i], dropout_keep_probability);
      }
    }
  }

  ans = outs.back();
  softmax(ans);
}

void NN::train(const M& inputs, const M& examples) {
  // Scale each example's contribution to obtain the mean batch
  // gradient.
  const double normalizer = 1.0 / static_cast<double>(inputs.size());

  for (std::size_t i = 0; i < inputs.size(); ++i) {
    input = inputs[i];
    forward_pass(false);
    back_propagation(examples[i], normalizer);
  }

  apply_grads();
}

V NN::predict(const V& in) {
  input = in;
  forward_pass(true);
  return ans;
}
