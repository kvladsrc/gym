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

int64_t bin_search(vector<int64_t> &a, int64_t el) {
  int64_t l = 0, r = a.size() - 1;
  int64_t res = -1;
  while (l <= r) {
    int64_t mid = (l + r) / 2;
    if (a[mid] >= el) {
      res = mid;
      r = mid - 1;
    } else {
      l = mid + 1;
    }
  }
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int64_t n;
  std::cin >> n;
  vector<int64_t> a(n);
  int64_t total = 0;
  for (auto &i : a) {
    std::cin >> i;
    total += i;
  }
  sort(a.begin(), a.end());

  int64_t m;
  std::cin >> m;
  while (m--) {
    int64_t x, y;
    std::cin >> x >> y;

    auto pos = bin_search(a, x);
    if (pos == -1) {
      pos = a.size() - 1;
    }

    int64_t case1 = 0;
    if ((total - a[pos]) < y) {
      case1 += y - (total - a[pos]);
    }
    if (a[pos] < x) {
      case1 += x - a[pos];
    }

    int64_t case2 = case1;
    if (pos > 0) {
      case2 = 0;
      if ((total - a[pos - 1]) < y) {
        case2 += y - (total - a[pos - 1]);
      }
      if (a[pos - 1] < x) {
        case2 += x - a[pos - 1];
      }
    }

    std::cout << min(case1, case2) << "\n";
  }

  return 0;
}
