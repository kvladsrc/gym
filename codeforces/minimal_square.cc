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
    int a, b;
    std::cin >> a >> b;
    int min_side, max_side;
    min_side = min(a, b);
    max_side = max(a, b);
    if (min_side * 2 <= max_side)
      std::cout << max_side * max_side << "\n";
    else
      std::cout << (min_side * 2) * (min_side * 2) << "\n";
  }

  return 0;
}
