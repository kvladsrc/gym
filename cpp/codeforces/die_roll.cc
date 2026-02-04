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
  int Y, W;
  std::cin >> Y >> W;
  int needed = 6 - max(Y, W) + 1;

  switch (needed) {
    case 1:
      std::cout << "1/6"
                << "\n";
      break;
    case 2:
      std::cout << "1/3"
                << "\n";
      break;
    case 3:
      std::cout << "1/2"
                << "\n";
      break;
    case 4:
      std::cout << "2/3"
                << "\n";
      break;
    case 5:
      std::cout << "5/6"
                << "\n";
      break;
    case 6:
      std::cout << "1/1"
                << "\n";
      break;
  }

  return 0;
}
