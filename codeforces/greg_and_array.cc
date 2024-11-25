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

int binSearch(vector<int> &a, int el) {
  int res = -1;
  int l = 0, r = a.size() - 1;
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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, m, k;
  std::cin >> n >> m >> k;

  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  vector<int64_t> val(m);
  vector<vector<int>> l_ops(n + 1);
  vector<vector<int>> r_ops(n + 1);
  for (int i = 0; i < m; ++i) {
    int l, r, d;
    std::cin >> l >> r >> d;
    val[i] = d;
    l_ops[l - 1].push_back(i + 1);
    r_ops[r].push_back(i + 1);
  }

  vector<int> ql(k);
  vector<int> qr(k);
  for (int i = 0; i < k; ++i) {
    int l, r;
    std::cin >> l >> r;
    ql[i] = l;
    qr[i] = r;
  }
  sort(ql.begin(), ql.end());
  sort(qr.begin(), qr.end());

  int64_t delta = 0;
  map<int, int64_t> active_ops;
  for (int i = 0; i < n; ++i) {
    for (auto op : l_ops[i]) {
      auto r_pos = binSearch(qr, op);
      if (r_pos == -1) {
        r_pos = k;
      }

      auto l_pos = binSearch(ql, op + 1);
      if (l_pos == -1) {
        l_pos = k;
      }
      auto coefs = l_pos - r_pos;
      active_ops[op] = val[op - 1] * coefs;
      delta += active_ops[op];
    }

    for (auto op : r_ops[i]) {
      delta -= active_ops[op];
    }

    a[i] += delta;
    std::cout << a[i] << " ";
  }
  std::cout << "\n";

  return 0;
}
