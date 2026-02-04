#include <iostream>
#include <string>

using std::string;

int main(int argc, char* argv[]) {
  string word, lower, upper;
  std::cin >> word;
  int upper_num = 0;
  for (char c : word) {
    if (c == std::toupper(c)) {
      upper_num++;
    }

    upper += std::toupper(c);
    lower += std::tolower(c);
  }

  if (upper_num > word.size() / 2.0)
    std::cout << upper << "\n";
  else
    std::cout << lower << "\n";

  return 0;
}
