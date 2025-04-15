#include <cstdint>
#include <iostream>
#include <string>

using std::string;

namespace {

/*
 * Compute x^y mod m by O(log y) time complexity.
 */

const int64_t modulo = 1000000007;

int64_t fast_power(int64_t x, int64_t y, int64_t m) {
  if (y == 1) {
    return x;
  }

  if (y == 0) {
    return 1;
  }

  if ((y % 2) != 0) {
    auto buf = fast_power(x, y - 1, m);
    buf = (buf * x) % m;
    return buf;
  }

  return fast_power((x * x) % m, y / 2, m);  // x^y = (x^2)^y/2
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int64_t x = 0;
  int64_t y = 0;
  std::cin >> x >> y;

  std::cout << fast_power(x, y, modulo) << "\n";

  return 0;
}
