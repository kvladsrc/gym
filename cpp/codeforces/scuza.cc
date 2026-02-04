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

int binary_search_with_duplicates(const vector<int>& a, int el) {
  int l = 0, r = a.size() - 1;
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, q;
    std::cin >> n >> q;

    vector<int64_t> pref_sums(n + 1);
    vector<int> max_steps(n + 1);
    pref_sums[0] = 0;
    pref_sums[0] = 0;
    for (int i = 1; i <= n; ++i) {
      int64_t buf;
      std::cin >> buf;

      pref_sums[i] = buf + pref_sums[i - 1];
      max_steps[i] = max(static_cast<int>(buf), max_steps[i - 1]);
    }

    while (q--) {
      int query;
      std::cin >> query;

      auto pos = binary_search_with_duplicates(max_steps, query + 1);
      if (pos == -1) {
        std::cout << pref_sums.back() << " ";
      } else {
        std::cout << pref_sums[pos - 1] << " ";
      }
    }

    std::cout << "\n";
  }

  return 0;
}
