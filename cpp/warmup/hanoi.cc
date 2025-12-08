#include "cpp/warmup/hanoi.hpp"

#include <iostream>

namespace warmup {

int hanoi(int first, int second, int third, int n, std::ostream* out) {
  int res = 0;

  if (n == 0) {
    return res;
  }

  res += hanoi(first, third, second, n - 1, out);
  if (out) {
    *out << "Move disk from: " << first << " to " << third << "\n";
  }
  res++;
  res += hanoi(second, first, third, n - 1, out);

  return res;
}

}  // namespace warmup
