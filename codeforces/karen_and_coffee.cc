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

int binary_search(vector<int> &a, int el) {
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

  int n, k, q;
  std::cin >> n >> k >> q;

  vector<int> l(n), r(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> l[i] >> r[i];
  }

  vector<int> ql(q), qr(q);
  int maxqr = 0;
  for (int i = 0; i < q; ++i) {
    std::cin >> ql[i] >> qr[i];
    maxqr = max(maxqr, qr[i]);
  }

  // a_i is for number of admissible receipts between 0 and i
  // degree. For l and r answer if A[r] - A[l].
  sort(l.begin(), l.end());
  sort(r.begin(), r.end());

  vector<int> ps(maxqr + 1);
  ps.front() = 0;
  for (int i = 1; i <= maxqr; ++i) {
    auto rpos = binary_search(r, i);
    auto lpos = binary_search(l, i + 1);
    ps[i] = ps[i - 1];
    if (rpos == -1) {
      rpos = n;
    }
    if (lpos == -1) {
      lpos = n;
    }

    if (lpos - rpos >= k) {
      ps[i]++;
    }
  }

  for (int i = 0; i < q; ++i) {
    auto res = ps[qr[i]] - ps[ql[i] - 1];
    std::cout << res << "\n";
  }

  return 0;
}
