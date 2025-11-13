#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int binary_search(vector<int> &a, int i) {
  int l = 0, r = a.size() - 1;
  int res = -1;

  while (l <= r) {
    int mid = (l + r) / 2;

    if (a[mid] > i || a[mid] == i) {
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
    int n, l, r;
    std::cin >> n >> l >> r;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }
    sort(a.begin(), a.end());

    int64_t pairs = 0;
    for (int i = 0; i < n; ++i) {
      int l_tmp = binary_search(a, l - a[i]);
      if (l_tmp == -1) continue;
      l_tmp = max(i + 1, l_tmp);
      int r_tmp = binary_search(a, (r - a[i]) + 1);

      if (r_tmp != -1 && r_tmp <= l_tmp) continue;

      if (r_tmp == -1) {
        pairs += n - l_tmp;
      } else {
        pairs += r_tmp - l_tmp;
      }
    }

    std::cout << pairs << "\n";
  }

  return 0;
}
