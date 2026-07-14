#ifndef NEURAL_NETWORK_NN_LIB_H_
#define NEURAL_NETWORK_NN_LIB_H_

#include <cstddef>
#include <vector>

// Vector.
using V = std::vector<double>;

// Matrix.
using M = std::vector<V>;

class NNIO;

class NN {
 public:
  explicit NN(std::size_t in, double astep_size, double regularization);

  ~NN() = default;

  void add_layer(std::size_t out);
  void train(const M& inputs, const M& examples);
  V predict(const V& in);

 private:
  friend class NNIO;

  NN();
  void forward();
  void apply_grads();
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
