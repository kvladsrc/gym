#include <iostream>
#include <string>

using std::string;

int main(int argc, char *argv[]) {
  string input;
  std::cin >> input;
  input[0] = std::toupper(input[0]);
  std::cout << input << "\n";
  return 0;
}
