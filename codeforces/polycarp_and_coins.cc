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
    int n, one, two;
    std::cin >> n;
    one = two = n / 3;

    switch (n % 3) {
      case 1:
        one++;
        break;
      case 2:
        two++;
        break;
    }

    std::cout << one << " " << two << "\n";
  }
  return 0;
}
