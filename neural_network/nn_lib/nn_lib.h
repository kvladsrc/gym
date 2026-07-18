#ifndef NEURAL_NETWORK_NN_LIB_H_
#define NEURAL_NETWORK_NN_LIB_H_

#include <cstddef>
#include <vector>

// Vector.
using V = std::vector<double>;

// Matrix.
using M = std::vector<V>;

// Returns the ReLU activation of a scalar.
double relu(double value);

// Applies ReLU element-wise.
void activate(V& values);

// Applies inverted dropout in place.
void dropout(V& values, double keep_probability);

// Converts logits to probabilities in place.
void softmax(V& values);

class NNIO;

// A fully connected network with ReLU hidden layers and a softmax
// output.
class NN {
 public:
  // Creates a network with the given input width and optimizer
  // settings.
  explicit NN(std::size_t in, double astep_size, double regularization,
              double dropout_keep_probability = 1.0);

  ~NN() = default;

  // Appends a fully connected layer with `out` neurons.
  void add_layer(std::size_t out);

  // Performs one mini-batch gradient update.
  void train(const M& inputs, const M& examples);

  // Returns class probabilities for one input.
  V predict(const V& in);

 private:
  friend class NNIO;

  NN();

  // Runs the forward pass for the current input.
  void forward_pass(bool inference);

  // Applies accumulated gradients and clears their buffers.
  void apply_grads();

  // Accumulates gradients for one training example.
  void back_propagation(const V& example, double normalizer);

  // Filled by forward pass.
  std::vector<V> outs;
  V input;
  V ans;

  // Filled by back propagation.
  std::vector<M> grads;
  std::vector<V> biases_grads;

  std::size_t input_size;
  std::vector<M> network;
  std::vector<V> biases;
  double step_size;
  double regularization;
  double dropout_keep_probability;
};

#endif  // NEURAL_NETWORK_NN_LIB_H_
