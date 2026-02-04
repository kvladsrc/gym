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
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
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

bool solve(const std::vector<int>& a, std::map<int, int>& m, int lim, int k) {
  int added_odd = 0;
  for (size_t idx = 0; idx < a.size(); ++idx) {
    if (m[a[idx]] <= lim) {
      added_odd++;
      if (idx != a.size() - 1) {
        added_odd++;
        idx++;
      }
    }
    if (added_odd >= k) {
      return true;
    }
  }

  int added_even = 0;
  size_t last_added = 0;
  for (size_t idx = 1; idx < a.size(); ++idx) {
    if (m[a[idx]] <= lim) {
      last_added = idx;
      added_even += 2;
      idx++;
    }
    if (added_even >= k) {
      return true;
    }
  }
  if (last_added != a.size() - 1) {
    added_even++;
  }

  return added_even >= k;
}

int main(int /*argc*/, char* /*argv*/[]) {
  size_t n = 0;
  int k = 0;
  std::cin >> n;
  std::cin >> k;

  vector<int> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  auto a_cpy = a;
  sort(a_cpy.begin(), a_cpy.end());
  map<int, int> m;
  for (size_t idx = 0; idx < n; ++idx) {
    m[a_cpy[idx]] = idx;
  }

  int l = 0;
  int r = n - 1;
  int res = -1;

  while (l <= r) {
    auto mid = (l + r) / 2;
    if (solve(a, m, mid, k)) {
      res = mid;
      r = mid - 1;
    } else {
      l = mid + 1;
    }
  }

  std::cout << a_cpy[res] << "\n";

  return 0;
}
