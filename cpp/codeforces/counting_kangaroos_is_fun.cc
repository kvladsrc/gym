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

  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  /*
   * We cannot get less than N / 2 kangaroos.
   */

  sort(a.begin(), a.end());

  int r = n - 1;
  int m = r / 2;
  int res = n;

  while (m >= 0 && (r > ((n - 1) / 2))) {
    if (a[m] * 2 <= a[r]) {
      res--;
      r--;
      m--;
      continue;
    }

    m--;
  }

  std::cout << res << "\n";

  return 0;
}
