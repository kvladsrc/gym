#include "cpp/warmup/josephus.hpp"

namespace warmup {

int josephus(int n, int k) {
  if (n == 1) {
    return 0;
  }

  return (josephus(n - 1, k) + k) % n;
}

}  // namespace warmup
