#ifndef NEURAL_NETWORK_NN_LIB_H_
#define NEURAL_NETWORK_NN_LIB_H_

#include <cstddef>
#include <vector>

// Vector.
using V = std::vector<double>;

// Matrix.
using M = std::vector<V>;

class NNIO;

// A fully connected network with ReLU hidden layers and a softmax
// output.
class NN {
 public:
  // Creates a network with the given input width and optimizer
  // settings.
  explicit NN(std::size_t in, double astep_size, double regularization);

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
  void forward();

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
};

#endif  // NEURAL_NETWORK_NN_LIB_H_
