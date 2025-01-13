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

int64_t gcd(int64_t a, int64_t b) {
  if (a % b == 0) {
    return abs(b);
  }
  return gcd(b, a % b);
}

int64_t lcm(int64_t a, int64_t b) { return (a * b) / gcd(a, b); }

vector<int64_t> erato() {
  vector<bool> table(1000001, true);
  vector<int64_t> res;
  for (int i = 2; i < 1000001; ++i) {
    if (table[i]) {
      res.push_back(i);

      for (int j = 2; i * j < 1000001; ++j) {
        table[i * j] = false;
      }
    }
  }

  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;

  auto primes = erato();

  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;
    set<int64_t> divs;

    for (size_t idx = 0; idx < n; ++idx) {
      int64_t buf;
      std::cin >> buf;
      divs.insert(buf);
    }

    int64_t l = 1;
    for (auto d : divs) {
      auto lnew = lcm(l, d);

      if (lnew < l) {
        l = -1;
        break;
      }

      l = lnew;
    }

    if (l != -1 && divs.find(l) != divs.end()) {
      l *= *divs.begin();
    }

    for (int64_t d = 2; (d * d) <= l; ++d) {
      if ((l % d) != 0) {
        continue;
      }

      if (divs.find(d) == divs.end()) {
        l = -1;
        break;
      }

      if (divs.find(l / d) == divs.end()) {
        l = -1;
        break;
      }
    }

    std::cout << l << "\n";
  }

  return 0;
}
