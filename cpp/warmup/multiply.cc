#include "cpp/warmup/multiply.hpp"

namespace warmup {

int multiply(int a, int b) {
  if (a == 1) {
    return b;
  }

  if (a % 2 == 0) {
    return multiply(a / 2, b * 2);
  }

  return b + multiply(a / 2, b * 2);
}

}  // namespace warmup
