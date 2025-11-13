#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int gcd(int a, int b) {
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

int good(vector<int> &a, size_t k) {
  if (k == a.size()) {
    return 1;
  }

  /*

    A = aq + r
    B = bq + r

    A + d = B
    (aq + r) - (bq + r) = d
    aq - bq = d
    q(a - b) = d
    a - b = d/q -> d % q = 0

       3, 5, 10, 9, 17,23,22
    2: 1, 1, 0,  1, 1, 1, 0
    3: 0, 2, 1,  0, 2, 2, 1
    4: 3, 1, 2,  1, 1, 3, 2
    5: 3, 0, 0,  4, 2, 3, 2
    6: 3, 5, 4,  3, 5, 5, 4
    7: 3, 5, 3,  2, 3, 2, 1
    8: 3, 5, 2,  1, 1, 7, 6
    9: 3, 5, 1,  0, 8, 5, 4
   10: 3, 5, 0,  9, 7, 3, 2
   11: 3, 5, 10, 9, 6, 1, 0
   12: 3, 5, 10, 9, 5, 11,10
   13: 3, 5, 10, 9, 4, 10,9
   14: 3, 5, 10, 9, 3, 9, 8
   15: 3, 5, 10, 9, 2, 8, 7
   16: 3, 5, 10, 9, 1, 7, 6
   17: 3, 5, 10, 9, 0, 6, 5
   18: 3, 5, 10, 9, 17,5, 4

  */

  int g = 0;
  for (size_t idx = 0; idx < k; ++idx) {
    for (size_t i = idx + k; i < a.size(); i += k) {
      auto d = abs(a[i - k] - a[i]);
      g = gcd(g, d);
      if (g == 1) {
        return 0;
      }
    }
  }

  return 1;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    int res = 0;

    for (size_t k = 1; k * k <= n; ++k) {
      if (n % k != 0) {
        continue;
      }

      auto fk = k;
      auto sk = n / k;

      res += good(a, fk);
      if (sk != fk) {
        res += good(a, sk);
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
