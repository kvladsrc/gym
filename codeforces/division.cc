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

// For Division 1: 1900≤rating
// For Division 2: 1600≤rating≤1899
// For Division 3: 1400≤rating≤1599
// For Division 4: rating≤1399

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int rating;
    std::cin >> rating;
    if (rating >= 1900) {
      std::cout << "Division 1" << std::endl;
    } else if (rating >= 1600) {
      std::cout << "Division 2" << std::endl;
    } else if (rating >= 1400) {
      std::cout << "Division 3" << std::endl;
    } else {
      std::cout << "Division 4" << std::endl;
    }
  }

  return 0;
}
