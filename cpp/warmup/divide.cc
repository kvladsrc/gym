#include "cpp/warmup/divide.hpp"

#include <utility>

using std::pair;

namespace warmup {

pair<int, int> divide(int a, int b) {
  if (a < b) {
    return {0, a};
  }

  auto buf = divide(a / 2, b);
  buf.first *= 2;
  buf.second *= 2;
  buf.second += a % 2;
  if (buf.second >= b) {
    buf.second -= b;
    buf.first++;
  }
  return buf;
}

}  // namespace warmup
