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
using std::gcd;
using std::map;
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
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    // Lets store all i where a_i != a_i+1. Looking for a l<=i<=r by
    // binary search.
    vector<int> indecies;
    for (int i = 1; i < n; ++i) {
      if (a[i] != a[i - 1]) {
        indecies.push_back(i);
      }
    }

    int q;
    std::cin >> q;
    while (q--) {
      int l, r;
      std::cin >> l >> r;

      int res_l = -1, res_r = -1;
      auto pos = binary_search_with_duplicates(indecies, l);
      if (pos != -1 && indecies[pos] + 1 <= r) {
        res_l = indecies[pos];
        res_r = indecies[pos] + 1;
      }
      std::cout << res_l << " " << res_r << "\n";
    }
  }

  return 0;
}
