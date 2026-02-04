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

    vector<int> cypher(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> cypher[i];
    }

    for (int i = 0; i < n; ++i) {
      int moves;
      std::cin >> moves;

      while (moves--) {
        char move;
        std::cin >> move;

        if (move == 'U') {
          cypher[i]--;
          if (cypher[i] < 0) {
            cypher[i] = 9;
          }
        } else {
          cypher[i] = (cypher[i] + 1) % 10;
        }
      }
    }

    for (int i : cypher) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
