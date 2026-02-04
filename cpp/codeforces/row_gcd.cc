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

int64_t gcd_iterative(int64_t a, int64_t b) {
  if (b == 0) {
    return abs(a);
  }

  if (a == 0) {
    return abs(b);
  }

  while ((a % b) != 0) {
    auto buf = a;
    a = b;
    b = buf % b;
  }

  return abs(b);
}

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, m;
  std::cin >> n >> m;

  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  vector<int64_t> b(m);
  for (auto& i : b) {
    std::cin >> i;
  }

  if (n == 1) {
    for (auto i : b) {
      std::cout << i + a.front() << " ";
    }
    return 0;
  }

  auto f = a.front();
  auto g = std::accumulate(
      a.begin() + 1, a.end(), static_cast<int64_t>(0),
      [f](int64_t acc, int64_t val) { return gcd_iterative(acc, val - f); });

  for (auto i : b) {
    auto res = gcd_iterative(g, f + i);
    std::cout << res << " ";
  }

  return 0;
}
