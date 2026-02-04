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
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    int winner = -1;
    int winner_score = -1;
    for (int i = 1; i <= n; ++i) {
      int a, b;
      std::cin >> a >> b;
      if (a <= 10 && b > winner_score) {
        winner_score = b;
        winner = i;
      }
    }
    std::cout << winner << "\n";
  }
  return 0;
}
