#ifndef NEURAL_NETWORK_NN_IO_H_
#define NEURAL_NETWORK_NN_IO_H_

#include <string>

#include "nn_lib.h"  // NOLINT(build/include_subdir)

class NNIO {
 public:
  NNIO() = delete;

  static void save(const NN& net, const std::string& path);
  static NN load(const std::string& path);
};

#endif  // NEURAL_NETWORK_NN_IO_H_
