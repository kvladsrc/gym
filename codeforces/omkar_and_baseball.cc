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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    // Two operations is always enought. By first operation we can
    // move each element of the array to "wrong" position and sort
    // them by second.
    int n;
    std::cin >> n;
    vector<int> wrong_pos;
    int last_wrong = -1;
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;

      if (i == buf) {
        continue;
      }

      if (i != last_wrong + 1) {
        wrong_pos.push_back(i);
      }

      last_wrong = i;
    }

    std::cout << min(static_cast<int>(wrong_pos.size()), 2) << "\n";
  }

  return 0;
}
