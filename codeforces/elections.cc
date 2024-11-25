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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b, c;
    std::cin >> a >> b >> c;
    int win_score = max(max(a, b), c);

    if (win_score > b && win_score > c) {
      std::cout << 0 << " ";
    } else {
      std::cout << win_score - a + 1 << " ";
    }

    if (win_score > a && win_score > c) {
      std::cout << 0 << " ";
    } else {
      std::cout << win_score - b + 1 << " ";
    }

    if (win_score > a && win_score > b) {
      std::cout << 0 << "\n";
    } else {
      std::cout << win_score - c + 1 << "\n";
    }
  }

  return 0;
}
