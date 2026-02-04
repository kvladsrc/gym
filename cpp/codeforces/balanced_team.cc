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
    int m = (l + r) / 2;
    if (a[m] >= el) {
      r = m - 1;
      res = m;
    } else {
      l = m + 1;
    }
  }

  return res;
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;

  vector<int> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  sort(a.begin(), a.end());

  int res = 0;
  for (int i = 0; i < n; ++i) {
    int to_large = a[i] + 6;
    auto pos = binary_search_with_duplicates(a, to_large);
    int cur = (pos == -1 ? n - i : pos - i);
    res = max(res, cur);
  }
  std::cout << res << "\n";

  return 0;
}
