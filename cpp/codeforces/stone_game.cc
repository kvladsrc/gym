#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    int max_val = 0;
    int min_val = n + 1;
    int max_pos = -1;
    int min_pos = -1;
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      if (buf > max_val) {
        max_val = buf;
        max_pos = i;
      }
      if (buf < min_val) {
        min_val = buf;
        min_pos = i;
      }
    }

    int case1 = max(min_pos, max_pos);
    int case2 = n - min(min_pos, max_pos);
    case2++;  // 12345 -- need to delete (5 - 3) from end to remove 3
    int case3 = min(min_pos, max_pos) + n - max(min_pos, max_pos);
    case3++;

    std::cout << min(case3, min(case1, case2)) << "\n";
  }
  return 0;
}
