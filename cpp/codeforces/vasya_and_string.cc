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

int binary_search(const vector<int>& a, int el) {
  int res = -1;
  int l = 0, r = a.size() - 1;
  while (l <= r) {
    int mid = (l + r) / 2;
    if (a[mid] > el) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, k;
  std::cin >> n >> k;

  vector<int> b_pref_sum(n + 1);
  vector<int> a_pref_sum(n + 1);
  b_pref_sum.front() = 0;
  a_pref_sum.front() = 0;

  for (int idx = 1; idx <= n; ++idx) {
    char buf;
    std::cin >> buf;
    a_pref_sum[idx] = a_pref_sum[idx - 1];
    b_pref_sum[idx] = b_pref_sum[idx - 1];
    if (buf == 'b') {
      b_pref_sum[idx]++;
    } else {
      a_pref_sum[idx]++;
    }
  }

  int mx = 0;
  for (int l = 1; l <= n; ++l) {
    int if_a = binary_search(a_pref_sum, a_pref_sum[l - 1] + k);
    if (if_a == -1) {
      if_a = n - (l - 1);
    } else {
      if_a = (if_a - 1) - (l - 1);
    }
    mx = max(mx, if_a);

    int if_b = binary_search(b_pref_sum, b_pref_sum[l - 1] + k);
    if (if_b == -1) {
      if_b = n - (l - 1);
    } else {
      if_b = (if_b - 1) - (l - 1);
    }
    mx = max(mx, if_b);
  }

  std::cout << mx << "\n";

  return 0;
}
