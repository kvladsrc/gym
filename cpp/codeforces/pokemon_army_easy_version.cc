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
#include <utility>
#include <vector>

using std::abs;
using std::gcd;
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

int64_t solve(vector<int> &a, bool add, size_t start) {
  if (start >= a.size()) {
    return 0;
  }

  auto case1 = solve(a, add, start + 1);
  int64_t case2 = solve(a, !add, start + 1);
  if (add) {
    case2 += a[start];
  } else {
    case2 -= a[start];
  }

  return max(case1, case2);
}

int64_t solve(vector<int> &a) { return solve(a, true, 0); }

int64_t solve_dp(vector<int> &a) {
  pair<int64_t, int64_t> prev = {0, 0};

  for (size_t idx = 1; idx <= a.size(); ++idx) {
    auto case1_add = prev.first;
    auto case2_add = prev.second + a[a.size() - idx];

    auto case1_sub = prev.second;
    auto case2_sub = prev.first - a[a.size() - idx];

    prev.first = max(case1_add, case2_add);
    prev.second = max(case1_sub, case2_sub);
  }

  return prev.first;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, q;
    std::cin >> n >> q;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    auto res = solve_dp(a);
    std::cout << res << "\n";
  }

  return 0;
}
