#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
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
using std::map;
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

int64_t gcd(int64_t a, int64_t b) {
  if (a % b == 0) {
    return b;
  }
  return gcd(b, a % b);
}

int64_t lcm(int64_t a, int64_t b) {
  auto g = gcd(a, b);
  return (a * b) / g;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;

  int64_t res = 0;
  int64_t start = max(1, n - 10);
  for (int64_t a = start; a <= n; ++a) {
    for (int64_t b = a; b <= n; ++b) {
      auto flcm = lcm(a, b);
      for (int64_t c = b; c <= n; ++c) {
        auto local = lcm(flcm, c);
        res = max(res, local);
      }
    }
  }
  std::cout << res << "\n";

  return 0;
}
