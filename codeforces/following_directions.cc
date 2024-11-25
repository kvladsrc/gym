#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
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
    pair<int, int> pos = {0, 0};
    bool res = false;

    while (n--) {
      char move;
      std::cin >> move;

      switch (move) {
        case 'L':
          pos.first--;
          break;
        case 'R':
          pos.first++;
          break;
        case 'U':
          pos.second++;
          break;
        default:
          pos.second--;
      }

      if (pos == pair<int, int>(1, 1)) {
        res = true;
      }
    }

    if (res) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }
  return 0;
}
