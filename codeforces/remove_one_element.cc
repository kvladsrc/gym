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

size_t best_of_two(vector<int> &a, vector<int> &b) {
  auto res = max(a.size(), b.size());
  if (a.size() == 1 || b.size() == 1) {
    return res;
  }

  if (a.back() < b[1] || a[a.size() - 2] < b.front()) {
    return max(res, a.size() + b.size() - 1);
  }

  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  vector<vector<int>> segs;
  vector<int> buf;
  for (size_t idx = 0; idx < n; ++idx) {
    if (buf.empty() || buf.back() < a[idx]) {
      buf.push_back(a[idx]);
      continue;
    }

    segs.push_back(buf);
    buf.clear();
    buf.push_back(a[idx]);
  }
  segs.push_back(buf);

  auto res = segs.front().size();
  for (size_t idx = 1; idx < segs.size(); ++idx) {
    res = max(res, best_of_two(segs[idx - 1], segs[idx]));
  }

  std::cout << res << "\n";

  return 0;
}
