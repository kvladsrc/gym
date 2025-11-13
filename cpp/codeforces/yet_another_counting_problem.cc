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
#include <utility>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t gcd(int64_t a, int64_t b) {
  if (a == 0) {
    return b;
  }

  if (b == 0) {
    return a;
  }

  if (a % b == 0) {
    return abs(b);
  }

  return gcd(b, a % b);
}

int64_t lcm(int64_t a, int64_t b) { return (a * b) / gcd(a, b); }

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int64_t a = 0;
    int64_t b = 0;
    int q = 0;
    std::cin >> a >> b >> q;

    if (a > b) {
      std::swap(a, b);
    }

    /*

      (x mod a) mod b = (x mod b) mod a
      a < b

      Idea (no proof):

      1. For i, 0 <= i < b, (i mod a) mod b = i mod a = (i mod b) mod
      a.

      2. More general for every j, l = LCM(a, b), jl <= i < jl + b, (i
      mod a) mod b = i mod a = (i mod b) mod a.

      3. Answer is (LCM(a, b) - b) * r / LCM(a, b) + r % LCM(a, b) -
      min(b, r % LCM(a, b)) - (LCM(a, b) - b) * l / LCM(a, b) + l % LCM(a, b) -
      min(b, l % LCM(a, b))

     */

    auto lm = lcm(a, b);
    auto per_lm = lm - b;

    for (int64_t i = 0; i < q; ++i) {
      int64_t l = 0;
      int64_t r = 0;
      std::cin >> l >> r;
      l--;

      auto res = ((r / lm) * per_lm) + ((r % lm) - min(b - 1, r % lm));
      res -= ((l / lm) * per_lm) + ((l % lm) - min(b - 1, l % lm));

      std::cout << res << "\n";
    }
  }

  return 0;
}
