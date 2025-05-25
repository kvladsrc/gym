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

bool is_sorted(vector<int> &a) {
  for (size_t idx = 1; idx < a.size(); ++idx) {
    if (a[idx] < a[idx - 1]) {
      return false;
    }
  }
  return true;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int x = 0;
    std::cin >> n >> x;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    if (is_sorted(a)) {
      std::cout << 0 << "\n";
      continue;
    }

    int res = 0;
    for (int i = 0; i < n; ++i) {
      if (a[i] > x) {
        std::swap(a[i], x);
        res++;

        if (is_sorted(a)) {
          break;
        }
      }
    }

    if (is_sorted(a)) {
      std::cout << res << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
