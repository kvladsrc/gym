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

int gcd(int a, int b) { return b == 0 ? abs(a) : gcd(b, a % b); }

vector<int> factor(int n) {
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

static int upper_bound(const vector<int>& a, int i) {
  int l = 0;
  int r = a.size() - 1;
  int res = -1;

  while (l <= r) {
    int const mid = (l + r) / 2;
    if (a[mid] >= i) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int a = 0;
  int b = 0;
  std::cin >> a >> b;

  auto g = gcd(a, b);
  auto f = factor(g);
  sort(f.begin(), f.end());

  int q = 0;
  std::cin >> q;

  while ((q--) != 0) {
    int l = 0;
    int r = 0;
    std::cin >> l >> r;

    auto u = upper_bound(f, r);

    if (u == -1) {
      if (f.back() >= l) {
        std::cout << f.back() << "\n";
      } else {
        std::cout << -1 << "\n";
      }

      continue;
    }

    if (f[u] == r) {
      std::cout << f[u] << "\n";
      continue;
    }

    if (u == 0) {
      std::cout << -1 << "\n";
      continue;
    }

    if (f[u - 1] >= l) {
      std::cout << f[u - 1] << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
