#include <algorithm>
#include <cstddef>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

static int lis(const vector<int>& a) {
  vector<int> d(a.size(), 1);
  int res = 0;
  for (size_t i = 0; i < a.size(); ++i) {
    for (size_t j = 0; j < i; ++j) {
      if (a[j] < a[i]) {
        d[i] = max(d[i], d[j] + 1);
        res = max(res, d[i]);
      }
    }
  }
  return res;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> a[i];
  }
  std::cout << lis(a) << "\n";
}
