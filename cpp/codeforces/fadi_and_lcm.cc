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

int gcd(int a, int b) {
  if (a % b == 0) {
    return b;
  }

  return gcd(b, a % b);
}

int main(int argc, char* argv[]) {
  int64_t x;
  std::cin >> x;

  int64_t res = INT64_MAX;
  for (int64_t i = 1; i * i <= x; ++i) {
    // x / i >= i because of the condition.
    if (x % i == 0 && gcd(i, x / i) == 1) {
      res = min(x / i, res);
    }
  }

  std::cout << x / res << " " << res << "\n";

  return 0;
}
