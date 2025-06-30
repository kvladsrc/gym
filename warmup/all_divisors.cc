#include "cpp/warmup/all_divisors.hpp"

#include <algorithm>
#include <iostream>
#include <vector>

using std::sort;
using std::vector;

namespace warmup {

vector<int> all_divisors(int n) {
  vector<int> res;

  // EDGE_CASE: divisors of negative numbers.
  n = abs(n);

  for (int d = 1; d * d <= n; ++d) {
    if (n % d == 0) {
      res.push_back(d);
      auto s = n / d;
      if (d != s) {
        res.push_back(s);
      }
    }
  }

  return res;
}

}  // namespace warmup
