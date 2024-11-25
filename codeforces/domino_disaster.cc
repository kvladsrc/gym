#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

    string l, res;
    std::cin >> l;

    for (char c : l) {
      if (c == 'L') {
        res += 'L';
        continue;
      }

      if (c == 'R') {
        res += 'R';
        continue;
      }

      if (c == 'U') {
        res += 'D';
        continue;
      }

      if (c == 'D') {
        res += 'U';
        continue;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
