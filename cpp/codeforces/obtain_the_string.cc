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
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::optional;
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

optional<size_t> upper_bound(vector<size_t>& a, size_t i) {
  // EDGE_CASE: empty vector do not contain any elements.
  if (a.empty()) {
    return std::nullopt;
  }

  int l = 0;
  int r = a.size() - 1;
  std::optional<size_t> res = std::nullopt;

  while (l <= r) {
    auto const mid = (l + r) / 2;
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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    string s;
    string target;
    std::cin >> s >> target;

    map<char, vector<size_t>> chars;
    for (size_t idx = 0; idx < s.size(); ++idx) {
      chars[s[idx]].push_back(idx);
    }

    size_t cur = 0;
    int res = 1;

    for (size_t i = 0; i < target.size();) {
      if (chars[target[i]].empty()) {
        res = -1;
        break;
      }

      auto pos = upper_bound(chars[target[i]], cur);

      if (pos) {
        cur = chars[target[i]][*pos] + 1;
        i++;
      } else {
        res++;
        cur = 0;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
