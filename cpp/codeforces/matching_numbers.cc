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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    if (n % 2 == 0) {
      std::cout << "NO" << "\n";
      continue;
    }

    vector<int> f(n);
    vector<int> s(n);

    // TODO(myuser): use std::reverse.
    for (int i = 0; i < n; ++i) {
      if (i < (n / 2)) {
        f[i] = n / 2 - i;
      } else {
        f[i] = n - (i - (n / 2));
      }
      s[i] = (2 * n) - i;
    }

    std::cout << "YES" << "\n";
    for (int i = 0; i < n; ++i) {
      std::cout << f[i] << " " << s[i] << "\n";
    }
  }

  return 0;
}
