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

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;
  int x, y;
  y = n / 2;
  x = n % 2 ? 1 : 0;

  int moves = -1;

  // invariant: n = x + 2y
  while (y >= 0) {
    if ((x + y) % m == 0) {
      moves = x + y;
      break;
    }

    // x + 2y = x + 2(y - 1) + 2
    y--;
    x += 2;
  }

  std::cout << moves << "\n";

  return 0;
}
