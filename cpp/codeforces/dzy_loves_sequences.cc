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
  std::cin >> n;
  vector<int> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  vector<pair<int, int>> ss;
  size_t l = 0;
  for (size_t i = 1; i < n; ++i) {
    if (a[i] <= a[i - 1]) {
      ss.push_back({l, i - 1});
      l = i;
    }
  }
  ss.push_back({l, n - 1});

  int res = (ss.front().second - ss.front().first + 1);
  if (ss.size() > 1) {
    res++;
  }

  for (size_t i = 1; i < ss.size(); ++i) {
    res = max(res, ss[i].second - ss[i].first + 2);
    if (ss[i].second - ss[i].first == 0 ||
        ss[i - 1].second - ss[i - 1].first == 0) {
      continue;
    }

    if (a[ss[i].first] > (a[ss[i - 1].second - 1] + 1) ||
        (a[ss[i].first + 1] - 1) > a[ss[i - 1].second]) {
      res = max(res, ss[i].second - ss[i - 1].first + 1);
    }
  }
  std::cout << res << "\n";

  return 0;
}
