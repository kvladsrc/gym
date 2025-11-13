#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int binary_search_with_duplicates(vector<int64_t> &a, int64_t el) {
  int res = -1;
  int l = 0, r = a.size() - 1;
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
  int n, q;
  std::cin >> n >> q;
  vector<int64_t> a(n), l(q), r(q);
  for (auto &i : a) {
    std::cin >> i;
  }
  for (int i = 0; i < q; ++i) {
    std::cin >> l[i] >> r[i];
  }
  sort(l.begin(), l.end());
  sort(r.begin(), r.end());

  vector<int64_t> dot_in_segments(n);
  for (int i = 1; i <= n; ++i) {
    int64_t segments_count = binary_search_with_duplicates(l, i + 1);
    if (segments_count == -1) {
      segments_count = q;
    }
    int64_t right_filter = binary_search_with_duplicates(r, i);
    if (right_filter == -1) right_filter = q;

    segments_count -= right_filter;
    dot_in_segments[i - 1] = segments_count;
  }

  sort(dot_in_segments.begin(), dot_in_segments.end());
  sort(a.begin(), a.end());

  int64_t res = 0;
  for (int i = 0; i < n; ++i) {
    res += dot_in_segments[i] * a[i];
  }

  std::cout << res << "\n";

  return 0;
}
