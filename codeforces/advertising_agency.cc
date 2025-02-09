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

const int64_t modulo = 1000000007;

int fast_power(int a, int p) {
  if (p == 0) {
    return 1;
  }

  if (p % 2 == 0) {
    a = a * 1ll * a % modulo;
    return fast_power(a, p / 2);
  }

  auto r = fast_power(a, p - 1);
  r = r * 1ll * a % modulo;
  return r;
}

int factorial(int a) {
  int res = 1;
  for (int i = 1; i <= a; ++i) {
    res = res * 1ll * i % modulo;
  }
  return res;
}

int combinations(int k, int n) {
  auto r = factorial(n);
  auto d = factorial(k) * 1ll * factorial(n - k) % modulo;
  d = fast_power(d, modulo - 2);
  r = r * 1ll * d % modulo;

  return r;
}

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

// Inverse modulo element approach is better, that is also works
// somehow...
int64_t partial_perm(int64_t a, int64_t b) {
  // We need to use modulo only if no more dividers left.
  vector<int64_t> divs(a);
  vector<int64_t> muls(a);
  for (int64_t i = 0; i < a; ++i) {
    divs[i] = i + 1;
    muls[i] = b - i;
  }

  for (auto& m : muls) {
    vector<int64_t> next_divs;
    for (auto d : divs) {
      auto g = gcd(m, d);
      m /= g;
      if (g != d) {
        next_divs.push_back(d / g);
      }
    }
    divs = next_divs;

    if (divs.empty()) {
      break;
    }
  }

  int64_t res = 1;
  for (auto m : muls) {
    res = (res * m) % modulo;
  }

  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int k = 0;
    std::cin >> n >> k;

    map<int, int> m;
    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;
      m[buf]++;
    }

    auto it = m.rbegin();
    int64_t res = 1;
    while (it != m.rend()) {
      if (it->second <= k) {
        k -= it->second;
        it++;
        continue;
      }

      if (k == 0) {
        break;
      }

      res = combinations(k, it->second);
      break;
    }

    std::cout << res << "\n";
  }

  return 0;
}
