#include <algorithm>
#include <cstddef>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    sort(a.begin(), a.end());

    vector<int> res;
    int x = 0, y = n - 1;
    bool up = true;
    while (res.size() < static_cast<size_t>(n)) {
      if (up) {
        res.push_back(a[y--]);
      } else {
        res.push_back(a[x++]);
      }
      up = !up;
    }

    for (int j = n - 1; j >= 0; --j) {
      std::cout << res[j] << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
