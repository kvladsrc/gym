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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  size_t m = 0;
  std::cin >> n >> m;

  vector<int> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  vector<int> b(m);
  for (auto& i : b) {
    std::cin >> i;
  }

  // Brute force. Try every b_j for every a_i, store result of
  // previous attempts. Have an intuition that order matters, so try
  // both non-decreasing and non-descending oprders.

  int res = INT_MAX;

  for (size_t start_from = 0; start_from < n; ++start_from) {
    int case_from = 0;

    for (size_t i = 0; i < n; ++i) {
      auto best = (a[(start_from + i) % n] & b.front()) | case_from;

      for (size_t j = 0; j < m; ++j) {
        auto local = (a[(start_from + i) % n] & b[j]) | case_from;
        best = min(best, local);
      }

      case_from = best;
    }

    res = min(res, case_from);
  }

  std::cout << res << "\n";

  return 0;
}
