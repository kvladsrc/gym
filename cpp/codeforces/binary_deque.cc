#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int binary_search_with_duplicates(vector<int> &a, int el) {
  int l = 0, r = a.size() - 1;
  int res = -1;
  while (l <= r) {
    int mid = (l + r) / 2;
    if (a[mid] >= el) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, s;
    std::cin >> n >> s;
    vector<int> one_cnt(n + 1);
    one_cnt[0] = 0;
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      one_cnt[i] = one_cnt[i - 1] + buf;
    }

    // Using binary search trying to find minimal number of operations
    // as left oprations + right operations. Starting from all left
    // operations ending by all right operations.
    auto total = one_cnt.back();

    if (total < s) {
      std::cout << -1 << "\n";
      continue;
    } else if (total == s) {
      std::cout << 0 << "\n";
      continue;
    }

    int best = INT_MAX;
    for (int from_l = total - s; from_l >= 0; --from_l) {
      auto from_r = (total - s) - from_l;
      // 000011112222
      //     .
      // l = 4 = need to remove.
      auto l_ops = binary_search_with_duplicates(one_cnt, from_l);

      // 000011112222
      //         .
      // r = 8, n = 11, need to remove = 4, try n - r + 1
      int r_ops = 0;
      if (from_r) {
        r_ops =
            n - binary_search_with_duplicates(one_cnt, total - from_r + 1) + 1;
      }

      best = min(best, l_ops + r_ops);
    }

    std::cout << best << "\n";
  }

  return 0;
}
