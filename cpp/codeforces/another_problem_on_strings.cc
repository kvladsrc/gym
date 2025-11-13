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

int64_t lower_bound(vector<int64_t> &a, int el) {
  int l = 0;
  int r = a.size() - 1;
  int res = -1;
  while (l <= r) {
    int mid = (l + r) / 2;
    if (a[mid] >= el) {
      res = mid;
      r = mid - 1;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int k = 0;
  std::cin >> k;

  string s;
  std::cin >> s;

  vector<int64_t> pref_sum(s.size() + 1);
  pref_sum.front() = 0;
  for (size_t idx = 0; idx < s.size(); ++idx) {
    pref_sum[idx + 1] = pref_sum[idx];
    if (s[idx] == '1') {
      pref_sum[idx + 1]++;
    }
  }

  int64_t res = 0;
  for (size_t idx = 1; idx <= s.size(); ++idx) {
    auto r = lower_bound(pref_sum, pref_sum[idx - 1] + k + 1);
    if (r == -1) {
      r = s.size() + 1;
    }
    auto l = lower_bound(pref_sum, pref_sum[idx - 1] + k);
    if (l == -1) {
      continue;
    }
    l = max(l, static_cast<int64_t>(idx));
    res += r - l;
  }

  std::cout << res << "\n";

  return 0;
}
