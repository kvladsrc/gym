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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  for (size_t idx = 0; idx < n; ++idx) {
    if (a[idx] > static_cast<int>(idx + 1)) {
      std::cout << -1 << "\n";
      return 0;
    }
  }

  vector<int> b(n, -1);
  for (size_t idx = 1; idx < n; ++idx) {
    if (a[idx] != a[idx - 1]) {
      b[idx] = a[idx - 1];
    }
  }

  int prev = 0;
  if (a.front() == 1) {
    b.front() = 0;
  }

  stack<size_t> s;

  for (size_t idx = 0; idx < n; ++idx) {
    if (b[idx] == -1) {
      s.push(idx);
      continue;
    }

    prev++;

    while (prev < a[idx]) {
      auto c = s.top();
      s.pop();
      b[c] = prev++;
    }
  }

  while (!s.empty()) {
    auto c = s.top();
    s.pop();
    b[c] = static_cast<int>(n);
  }

  for (auto i : b) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
