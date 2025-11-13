#include "cpp/warmup/gorner.hpp"

#include <cstdint>
#include <iostream>
#include <vector>

using std::vector;

namespace warmup {

int64_t gorner(const vector<int64_t>& coefs, int64_t x) {
  int64_t res = 0;
  for (const auto c : coefs) {
    res *= x;
    res += c;
  }
  return res;
}

}  // namespace warmup
