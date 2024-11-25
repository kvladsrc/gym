#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t fast_power(int x, int y, int modulo) {
  if (y == 1)
    return x;
  if (y == 0)
    return 1;

  // x^y = (x^2)^y/2
  int64_t buf;
  if (y % 2) {
    buf = fast_power(x, y - 1, modulo);
    buf *= x;
  } else {
    buf = fast_power(x * x, y / 2, modulo);
  }

  buf %= modulo;
  return buf;
}

int main(int argc, char *argv[]) {
  int x, y;
  std::cin >> x >> y;

  int m = 1000000000;

  std::cout << fast_power(x, y, m) << "\n";

  return 0;
}
