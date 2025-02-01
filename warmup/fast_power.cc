#include <cstdint>
#include <iostream>
#include <string>

using std::string;

static int64_t fast_power(int x, int y, int modulo) {
  if (y == 1) {
    return x;
  }

  if (y == 0) {
    return 1;
  }

  // x^y = (x^2)^y/2
  int64_t buf = 0;
  if ((y % 2) != 0) {
    buf = fast_power(x, y - 1, modulo);
    buf *= x;
  } else {
    buf = fast_power(x * x, y / 2, modulo);
  }

  buf %= modulo;
  return buf;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int x = 0;
  int y = 0;
  std::cin >> x >> y;

  int const m = 1000000000;

  std::cout << fast_power(x, y, m) << "\n";

  return 0;
}
