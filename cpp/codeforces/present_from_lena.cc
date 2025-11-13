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
  int n;
  std::cin >> n;

  int max_len = 2 * n + 1;

  for (int row = 1; row <= max_len; ++row) {
    string line;
    for (int col = 1; col <= max_len; ++col) {
      int x_to_mid = col - (n + 1);
      int y_to_mid = row - (n + 1);

      if (abs(x_to_mid) + abs(y_to_mid) <= n) {
        line += (n - (abs(x_to_mid) + abs(y_to_mid))) + '0';
        line += " ";
      } else {
        line += "  ";
      }
    }

    // Remove ending spaces from line.
    line.erase(line.find_last_not_of(" ") + 1);
    std::cout << line << std::endl;
  }

  return 0;
}
